# Run CabinetNC Cut (local)

Path: `C:\Users\yino\Projects\cabinetnc-cut`

## Vite prototype (browser)
```powershell
cd C:\Users\yino\Projects\cabinetnc-cut
npm install
npm run dev
```
Open http://localhost:5177/

## .NET Desktop (WPF)
Requires .NET 10 SDK (already installed).
```powershell
cd C:\Users\yino\Projects\cabinetnc-cut\dotnet
dotnet run --project src\CabinetNC.Desktop -c Release
```
Or start: `dotnet\src\CabinetNC.Desktop\bin\Release\net10.0-windows\CabinetNC.Desktop.exe`

## Portable web shell
Unzip `CabinetNC-Cut-v0.1.0-portable.zip` and double-click `start.bat` (needs Node).
