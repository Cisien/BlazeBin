FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["BlazeBin/Server/BlazeBin.Server.csproj", "BlazeBin/Server/"]
COPY ["BlazeBin/Shared/BlazeBin.Shared.csproj", "BlazeBin/Shared/"]
COPY ["BlazeBin/Client/BlazeBin.Client.csproj", "BlazeBin/Client/"]
RUN dotnet restore "BlazeBin/Server/BlazeBin.Server.csproj"
COPY . .
WORKDIR "/src/BlazeBin/Server"
RUN dotnet build "BlazeBin.Server.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "BlazeBin.Server.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "BlazeBin.Server.dll"]