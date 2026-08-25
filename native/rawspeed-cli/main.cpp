#include <algorithm>
#include <cmath>
#include <cstdint>
#include <cstdio>
#include <cstring>
#include <memory>
#include <string>
#include <thread>
#include <vector>

#include "librawspeed/RawSpeed-API.h"

using std::uint8_t;
using std::uint16_t;
using rawspeed::CFAColor;

struct DemosaicJob
{
    rawspeed::Array2DRef<uint16_t> mosaic;
    uint32_t scaleTop;    // 0
    uint32_t scaleLeft;   // 0
    uint16_t *rgb;
    uint32_t rgbWidth;
    uint32_t rgbHeight;
    int patternCode;
    float wb[3];

    explicit DemosaicJob(rawspeed::Array2DRef<uint16_t> m)
        : mosaic(m), scaleTop(0), scaleLeft(0), rgb(nullptr), rgbWidth(0),
          rgbHeight(0), patternCode(0), wb{1, 1, 1}
    {
    }
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
                if (yy < 0 || yy >= j.mosaic.height()) continue;
                for (int dx = -2; dx <= 2; ++dx)
                {
                    int xx = (int)sx + dx;
                    if (xx < 0 || xx >= j.mosaic.width()) continue;
                    int c = cfaIndexOf(j.patternCode, (uint32_t)yy, (uint32_t)xx);
                    float v = (float)j.mosaic(yy, xx);
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

static bool render(rawspeed::RawImage raw, std::vector<uint16_t> &rgbOut, uint32_t &outW, uint32_t &outH)
{
    raw->cfa.setCFA(rawspeed::iPoint2D(2, 2), CFAColor::RED, CFAColor::GREEN,
                    CFAColor::GREEN, CFAColor::BLUE);

    const auto c00 = raw->cfa.getColorAt(0, 0);
    const auto c01 = raw->cfa.getColorAt(1, 0);
    const auto c10 = raw->cfa.getColorAt(0, 1);
    const auto c11 = raw->cfa.getColorAt(1, 1);

    int pattern = -1;
    if (c00 == CFAColor::GREEN && c11 == CFAColor::GREEN && c01 == CFAColor::RED && c10 == CFAColor::BLUE) pattern = 0;   // RGGB
    else if (c00 == CFAColor::GREEN && c11 == CFAColor::GREEN && c01 == CFAColor::BLUE && c10 == CFAColor::RED) pattern = 2; // BGGR
    else if (c00 == CFAColor::RED && c11 == CFAColor::BLUE && c01 == CFAColor::GREEN && c10 == CFAColor::GREEN) pattern = 1;   // GRBG
    else if (c00 == CFAColor::BLUE && c11 == CFAColor::RED && c01 == CFAColor::GREEN && c10 == CFAColor::GREEN) pattern = 3;   // GBRG

    if (pattern < 0)
    {
        fprintf(stderr, "rawspeed: unsupported CFA pattern\n");
        return false;
    }

    const uint32_t mosaicW = (uint32_t)raw->getUncroppedDim().x;
    const uint32_t mosaicH = (uint32_t)raw->getUncroppedDim().y;
    const uint32_t w = (uint32_t)raw->dim.x;
    const uint32_t h = (uint32_t)raw->dim.y;

    auto mosaicRef = raw->getU16DataAsUncroppedArray2DRef();

    double sum[3] = {0, 0, 0};
    uint64_t cnt[3] = {0, 0, 0};
    for (uint32_t y = 0; y < mosaicH; ++y)
        for (uint32_t x = 0; x < mosaicW; ++x)
        {
            int c = cfaIndexOf(pattern, y, x);
            float v = (float)mosaicRef(y, x);
            sum[c] += v;
            cnt[c]++;
        }
    float wb[3] = {1, 1, 1};
    for (int c = 0; c < 3; ++c)
        if (cnt[c] > 0 && cnt[1] > 0)
            wb[c] = (float)((sum[1] / cnt[1]) / (sum[c] / cnt[c]));
    wb[1] = 1.0f;

    rgbOut.assign((size_t)w * h * 3, 0);
    DemosaicJob job(mosaicRef);
    job.rgb = rgbOut.data();
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
        rawspeed::FileReader reader(argv[1]);
        auto [storage, buffer] = reader.readFile();
        rawspeed::RawParser parser(std::move(buffer));
        auto decoder = parser.getDecoder();
        decoder->failOnUnknown = false;

        rawspeed::RawImage raw = decoder->decodeRaw();

        std::vector<uint16_t> rgb;
        uint32_t w = 0, h = 0;
        if (!render(raw, rgb, w, h))
            return 1;

        FILE *f = fopen(argv[2], "wb");
        if (!f)
        {
            fprintf(stderr, "rawspeed: cannot write output\n");
            return 1;
        }
        bool eightBit = (argc > 3 && argv[3][0] == '8');
        if (eightBit)
            fprintf(f, "P6\n%u %u\n255\n", w, h);
        else
            fprintf(f, "P6\n%u %u\n65535\n", w, h);
        std::vector<uint8_t> row((size_t)w * (eightBit ? 3 : 6));
        for (uint32_t y = 0; y < h; ++y)
        {
            size_t o = 0;
            const uint16_t *src = rgb.data() + (size_t)y * w * 3;
            for (uint32_t x = 0; x < w; ++x)
            {
                uint16_t r = src[x * 3 + 0];
                uint16_t g = src[x * 3 + 1];
                uint16_t b = src[x * 3 + 2];
                if (eightBit)
                {
                    row[o++] = (uint8_t)(r >> 8);
                    row[o++] = (uint8_t)(g >> 8);
                    row[o++] = (uint8_t)(b >> 8);
                }
                else
                {
                    row[o++] = (uint8_t)(r >> 8);
                    row[o++] = (uint8_t)(r & 0xFF);
                    row[o++] = (uint8_t)(g >> 8);
                    row[o++] = (uint8_t)(g & 0xFF);
                    row[o++] = (uint8_t)(b >> 8);
                    row[o++] = (uint8_t)(b & 0xFF);
                }
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
