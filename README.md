# ParallelOrigin GameServer

The c# gameserver of parallel origin. 

## Run on linux

### Requirements
- .Net7 runtime with public IPv4
- PostGreSQL 14.6 with public IPv4 and "parallelorigin" database

### Preparing
- Update SQL connection string in `Program.cs` to the new IPv4, username and password
- `dotnet ef migrations add INIT` 
- `dotnet ef database update`
- Place `pnv_biome.type_biome00k_c_1km_s0..0cm_2000..2017_v0.1` in the `./Release/Net7.0/` folder. 

### Deployment
- Pull project
- `git submodule update --init --recursive`
- `./pullAndBuild`

