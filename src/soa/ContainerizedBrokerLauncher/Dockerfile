FROM mcr.microsoft.com/dotnet/framework/runtime:4.8-windowsservercore-ltsc2019
RUN powershell -Command Add-WindowsFeature -name NET-WCF-Pipe-Activation45
RUN powershell -Command Add-WindowsFeature NET-WCF-TCP-Activation45
RUN powershell -Command Add-WindowsFeature NET-WCF-HTTP-Activation45
RUN sc.exe config NetTcpPortSharing start=demand
RUN setx /m TELEPATHY_SERVICE_WORKING_DIR ^"C:\TelepathyServiceRegistration\\^"
EXPOSE 9087
WORKDIR /app
COPY bin/Release ./
CMD ContainerizedHpcBroker.exe -d --SessionAddress %SESSION_LAUNCHER_ADDRESS%
#docker container run -p 9087:9087 -p 9091:9091 -p 9093:9093 -it -e SESSION_ADDRESS=localhost brokerlauncher