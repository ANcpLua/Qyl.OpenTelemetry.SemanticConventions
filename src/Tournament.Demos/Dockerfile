FROM mcr.microsoft.com/dotnet/runtime:10.0@sha256:58318ab0733b63d3ac0d7609c46f2718244e623a176f45991ee01fad46fbf880 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:548d93f8a18a1acbe6cc127bc4f47281430d34a9e35c18afa80a8d6741c2adc3 AS build
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
