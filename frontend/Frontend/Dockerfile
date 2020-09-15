FROM mcr.microsoft.com/dotnet/core/aspnet:3.1
EXPOSE 9100
WORKDIR /app
COPY bin/Release/netcoreapp3.1 ./
ENTRYPOINT ["dotnet", "Frontend.dll", "9100"]