#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/core/runtime:3.1-buster-slim AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/core/sdk:3.1-buster AS build
WORKDIR /src
COPY ["Telepathy.HostAgent.Launcher/Telepathy.HostAgent.Launcher.csproj", "Telepathy.HostAgent.Launcher/"]
COPY ["Telepathy.HostAgent.Core/Telepathy.HostAgent.Core.csproj", "Telepathy.HostAgent.Core/"]
COPY ["Telepathy.HostAgent.Common/Telepathy.HostAgent.Common.csproj", "Telepathy.HostAgent.Common/"]
COPY ["Telepathy.HostAgent.Interface/Telepathy.HostAgent.Interface.csproj", "Telepathy.HostAgent.Interface/"]
RUN dotnet restore "Telepathy.HostAgent.Launcher/Telepathy.HostAgent.Launcher.csproj"
COPY . .
WORKDIR "/src/Telepathy.HostAgent.Launcher"
RUN dotnet build "Telepathy.HostAgent.Launcher.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Telepathy.HostAgent.Launcher.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Microsoft.Telepathy.HostAgent.Launcher.dll"]