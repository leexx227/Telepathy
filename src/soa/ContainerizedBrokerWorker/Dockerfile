FROM mcr.microsoft.com/dotnet/framework/runtime:4.8-windowsservercore-ltsc2019
RUN powershell -Command Add-WindowsFeature -name NET-WCF-Pipe-Activation45
RUN powershell -Command Add-WindowsFeature NET-WCF-TCP-Activation45
RUN powershell -Command Add-WindowsFeature NET-WCF-HTTP-Activation45
RUN sc.exe config NetTcpPortSharing start=demand
EXPOSE 9091
EXPOSE 9093
WORKDIR /app
COPY bin/Release ./
ENTRYPOINT ["ContainerizedBrokerWorker.exe"]