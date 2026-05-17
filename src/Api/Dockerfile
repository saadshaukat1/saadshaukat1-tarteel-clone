FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore
COPY src/Api/TarteelClone.Api.csproj                       src/Api/
COPY src/QuranEngine/TarteelClone.QuranEngine.csproj        src/QuranEngine/
COPY src/UserService/TarteelClone.UserService.csproj        src/UserService/
COPY src/SearchService/TarteelClone.SearchService.csproj    src/SearchService/
RUN dotnet restore src/Api/TarteelClone.Api.csproj

# Build
COPY src/ src/
RUN dotnet publish src/Api/TarteelClone.Api.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "TarteelClone.Api.dll"]
