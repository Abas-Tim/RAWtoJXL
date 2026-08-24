#include <algorithm>
#include <cmath>
#include <cstdint>
#include <cstdio>
#include <cstring>
#include <memory>
#include <string>
#include <thread>
#include <vector>

#include "RawSpeed/RawSpeed.h"

using std::uint8_t;
using std::uint16_t;

struct DemosaicJob
{
    const uint16_t *mosaic;
    uint32_t mosaicWidth;
    uint32_t mosaicHeight;
    uint32_t scaleTop;    // 0
    uint32_t scaleLeft;   // 0
    uint16_t *rgb;
    uint32_t rgbWidth;
    uint32_t rgbHeight;
    int patternCode;
    float wb[3];
};

static int cfaIndexOf(int patternCode, uint32_t row, uint32_t col)
{
    // 0 = RED, 1 = GREEN, 2 = BLUE (rawSpeed CFAColor order)
    static const int code[4][4] =
    {
        {1, 0, 2, 1},  // 0 RGGB
        {0, 1, 1, 2},  // 1 GRBG
        {2, 1, 1, 0},  // 2 BGGR
        {1, 2, 0, 1}   // 3 GBRG
    };
    return code[patternCode][((row & 1) << 1) | (col & 1)];
}

static void demosaicBand(const DemosaicJob &j, uint32_t yStart, uint32_t yEnd)
{
    for (uint32_t y = yStart; y < yEnd && y < j.rgbHeight; ++y)
    {
        for (uint32_t x = 0; x < j.rgbWidth; ++x)
        {
            uint32_t sx = x + j.scaleLeft;
            uint32_t sy = y + j.scaleTop;

            float acc[3] = {0, 0, 0};
            int counts[3] = {0, 0, 0};
            for (int dy = -2; dy <= 2; ++dy)
            {
                int yy = (int)sy + dy;
                if (yy < 0 || yy >= (int)j.mosaicHeight) continue;
                for (int dx = -2; dx <= 2; ++dx)
                {
                    int xx = (int)sx + dx;
                    if (xx < 0 || xx >= (int)j.mosaicWidth) continue;
                    int c = cfaIndexOf(j.patternCode, yy, xx);
                    float v = j.mosaic[yy * j.mosaicWidth + xx];
                    float w = (dx == 0 && dy == 0) ? 4.0f
                             : (std::abs(dx) + std::abs(dy) == 1) ? 1.0f
                             : 0.25f;
                    acc[c] += v * w;
                    counts[c]++;
                }
            }

            float r = counts[0] ? acc[0] / (counts[0] * 2.0f) : acc[1] / std::max(1, counts[1]);
            float g = counts[1] ? acc[1] / (counts[1] * 2.0f) : 0;
            float b = counts[2] ? acc[2] / (counts[2] * 2.0f) : acc[1] / std::max(1, counts[1]);
            if (counts[1] == 0) g = (counts[0] ? r : b);

            r *= j.wb[0];
            g *= j.wb[1];
            b *= j.wb[2];

            uint16_t *dst = j.rgb + ((size_t)y * j.rgbWidth + x) * 3;
            dst[0] = (uint16_t)std::min(65535.0f, r);
            dst[1] = (uint16_t)std::min(65535.0f, g);
            dst[2] = (uint16_t)std::min(65535.0f, b);
        }
    }
}

static bool render(rawspeed::RawDecoder *decoder, std::vector<uint16_t> &rgbOut, uint32_t &outW, uint32_t &outH)
{
    auto raw = decoder->decodeRaw();
    if (!raw) return false;

    int cfa[4] = {
        raw->cfa->getColorAt(0, 0), raw->cfa->getColorAt(0, 1),
        raw->cfa->getColorAt(1, 0), raw->cfa->getColorAt(1, 1)
    };
    int pattern = -1;
    if (cfa[3] == 1 && cfa[0] == 1 && cfa[1] == 0 && cfa[2] == 2) pattern = 0;            // RGGB
    else if (cfa[3] == 2 && cfa[0] == 1 && cfa[1] == 1 && cfa[2] == 0) pattern = 1;      // GRBG
    else if (cfa[3] == 0 && cfa[0] == 1 && cfa[1] == 1 && cfa[2] == 2) pattern = 2;      // BGGR
    else if (cfa[3] == 1 && cfa[0] == 2 && cfa[1] == 0 && cfa[2] == 3) pattern = 3;      // skip
    // fall back pattern detection by unique positions
    if (pattern < 0)
    {
        // determine which 2x2 arrangement: find color at (0,0) and (0,1)
        int c00 = cfa[0]; int c01 = cfa[1]; int c10 = cfa[2]; int c11 = cfa[3];
        if (c00 == c11 && c01 == c10 && (c01 + c00) == 2)
            pattern = (c00 == 1) ? (c01 == 0 ? 0 : 2) : (c01 == 0 ? 1 : 3);
        else if (c00 == c01 && c10 == c11)
            pattern = (c00 == 1 && c10 == 2) ? 0 : 2; // approx
    }
    if (pattern < 0)
    {
        // count: green-only pairs accepted as 2x2; else unsupported
        fprintf(stderr, "rawspeed: unsupported CFA pattern\n");
        return false;
    }

    const uint32_t mosaicW = raw->getUncroppedDim().width;
    const uint32_t mosaicH = raw->getUncroppedDim().height;
    const uint32_t w = raw->getDim().width;
    const uint32_t h = raw->getDim().height;
    const uint16_t *mosaic = raw->getDataAsU16Array();

    double sum[3] = {0, 0, 0};
    uint64_t cnt[3] = {0, 0, 0};
    for (uint32_t y = 0; y < mosaicH; ++y)
        for (uint32_t x = 0; x < mosaicW; ++x)
        {
            int c = cfaIndexOf(pattern, y, x);
            sum[c] += mosaic[y * mosaicW + x];
            cnt[c]++;
        }
    float wb[3] = {1, 1, 1};
    for (int c = 0; c < 3; ++c)
        if (cnt[c] > 0 && cnt[1] > 0)
            wb[c] = (float)((sum[1] / cnt[1]) / (sum[c] / cnt[c]));
    wb[1] = 1.0f;

    rgbOut.assign((size_t)w * h * 3, 0);
    DemosaicJob job;
    job.mosaic = mosaic;
    job.mosaicWidth = mosaicW;
    job.mosaicHeight = mosaicH;
    job.rgb = rgbOut.data();
    job.rgbWidth = w;
    job.rgbHeight = h;
    job.patternCode = pattern;
    job.wb[0] = wb[0]; job.wb[1] = 1.0f; job.wb[2] = wb[2];
    job.scaleTop = 0;
    job.scaleLeft = 0;

    unsigned nThreads = std::min(48u, std::max(1u, std::thread::hardware_concurrency()));
    uint32_t band = (h + nThreads - 1) / nThreads;
    std::vector<std::thread> threads;
    for (unsigned t = 0; t < nThreads; ++t)
        threads.emplace_back(demosaicBand, job, t * band, (t + 1) * band);
    for (auto &th : threads) th.join();

    outW = w;
    outH = h;
    return true;
}

int main(int argc, char **argv)
{
    if (argc < 3)
    {
        fprintf(stderr, "usage: rawspeed-cli <in.raw> <out.ppm>\n");
        return 2;
    }

    try
    {
        rawspeed::RawParser parser(argv[1]);
        auto decoder = parser.getDecoder();
        if (!decoder)
        {
            fprintf(stderr, "rawspeed: unsupported file\n");
            return 1;
        }
        decoder->failOnUnknown = false;

        std::vector<uint16_t> rgb;
        uint32_t w = 0, h = 0;
        if (!render(decoder.get(), rgb, w, h))
            return 1;

        FILE *f = fopen(argv[2], "wb");
        if (!f)
        {
            fprintf(stderr, "rawspeed: cannot write output\n");
            return 1;
        }
        fprintf(f, "P6\n%u %u\n65535\n", w, h);
        std::vector<uint8_t> row((size_t)w * 6);
        for (uint32_t y = 0; y < h; ++y)
        {
            size_t o = 0;
            const uint16_t *src = rgb.data() + (size_t)y * w * 3;
            for (uint32_t x = 0; x < w; ++x)
            {
                uint16_t r = src[x * 3 + 0];
                uint16_t g = src[x * 3 + 1];
                uint16_t b = src[x * 3 + 2];
                row[o++] = (uint8_t)(r >> 8);
                row[o++] = (uint8_t)(r & 0xFF);
                row[o++] = (uint8_t)(g >> 8);
                row[o++] = (uint8_t)(g & 0xFF);
                row[o++] = (uint8_t)(b >> 8);
                row[o++] = (uint8_t)(b & 0xFF);
            }
            if (fwrite(row.data(), 1, o, f) != o) { fclose(f); return 1; }
        }
        fclose(f);
        fprintf(stderr, "rawspeed: done %ux%u\n", w, h);
        return 0;
    }
    catch (const std::exception &e)
    {
        fprintf(stderr, "rawspeed: error: %s\n", e.what());
        return 1;
    }
}
