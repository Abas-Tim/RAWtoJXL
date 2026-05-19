# RAWtoJXL

Windows desktop app (.NET 8, Avalonia) that converts RAW camera files to JPEG-XL, JPEG, or PNG.

**Input:** Sony (.ARW), Canon (.CR2/.CR3), Nikon (.NEF), Fujifilm (.RAF), Olympus (.ORF), Panasonic (.RW2), Adobe (.DNG)
**Output:** JPEG-XL (.JXL), JPEG (.JPG), PNG (.PNG)

## Build

```powershell
cd RAWtoJXL
./build.ps1
```

## Run

```powershell
cd RAWtoJXL
dotnet run --project RAWtoJXL.Avalonia
```

## Test

```powershell
cd RAWtoJXL
dotnet test
```
