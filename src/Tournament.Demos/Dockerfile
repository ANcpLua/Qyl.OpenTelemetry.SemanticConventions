FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/Tournament.Demos/Tournament.Demos.csproj", "src/Tournament.Demos/"]
RUN dotnet restore "src/Tournament.Demos/Tournament.Demos.csproj"
COPY . .
WORKDIR "/src/src/Tournament.Demos"
RUN dotnet build "./Tournament.Demos.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Tournament.Demos.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Tournament.Demos.dll"]
