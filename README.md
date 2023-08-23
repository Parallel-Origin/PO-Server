# ParallelOrigin GameServer

The c# gameserver of parallel origin. 

## Run locally
### Requirements
- .Net7 runtime

### Running
- cd into `/ParallelOriginGameServer/Core`
- `git submodule update --init --recursive`
- `cd ..`
- `dotnet build`
- copy `pnv_biome.type_biome00k_c_1km_s0..0cm_2000..2017_v0.1.tif` into `/bin/Debug/net7.0/`
- cd into `/bin/Debug/net7.0/`
- `dotnet run`

## Server-Deployment
### Requirements
- .Net7 runtime with public IPv4
- PostGreSQL 14.6 with public IPv4 and "parallelorigin" database

### Preparing
- Update SQL connection string in `GameDBContext.cs` to the new IPv4, username and password
- Set `inMemory` to false in `Program.cs` where the `GameDBContext` is initialized.
- `dotnet ef migrations add INIT` 
- `dotnet ef database update`
- Place `pnv_biome.type_biome00k_c_1km_s0..0cm_2000..2017_v0.1` in the `./Release/Net7.0/` folder. 

### Deployment
- Pull project
- `git submodule update --init --recursive`
- `./pullAndBuild`

