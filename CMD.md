
dotnet run --project src/Run/Run.csproj -- install -a "--version 3.3.3"

dotnet run --project src/Run/Run.csproj -- build
dotnet run --project src/Run/Run.csproj -- pack -a "/p:Version=0.1.0"