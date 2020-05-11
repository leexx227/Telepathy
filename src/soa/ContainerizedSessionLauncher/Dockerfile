FROM mcr.microsoft.com/dotnet/framework/runtime:4.8-windowsservercore-ltsc2019
RUN powershell -Command Add-WindowsFeature NET-WCF-TCP-Activation45
RUN powershell -Command Add-WindowsFeature NET-WCF-HTTP-Activation45
RUN sc.exe config NetTcpPortSharing start=demand
RUN setx /m TELEPATHY_SERVICE_WORKING_DIR ^"C:\TelepathyServiceRegistration\\^"
EXPOSE 9090
EXPOSE 9092
WORKDIR /app
COPY bin/Release ./
CMD ContainerizedSessionLauncher.exe -d --AzureBatchServiceUrl %BATCH_ACCOUNT_SERVICE_URL% --AzureBatchAccountName %BATCH_ACCOUNT_NAME% --AzureBatchAccountKey %BATCH_ACCOUNT_KEY% --AzureBatchPoolName %BATCH_POOL_NAME% --AzureBatchBrokerStorageConnectionString %DES_STORAGE_CONNECTION_STRING%
#docker container run -p 9090:9090 -p 9092:9092 -it -e BATCH_ACCOUNT_SERVICE_URL=https://telepathy.japaneast.batch.azure.com -e BATCH_ACCOUNT_NAME=telepathy -e BATCH_ACCOUNT_KEY="RaLd7C2fwOJin9v2VsCBmUeLZB9+fOmANLjXmWb5/z6/xk1K2h8uxeXy4/acoVLzlP+npHGZqOoY6oQj/OTahg==" -e BATCH_POOL_NAME=telepathy -e DES_STORAGE_CONNECTION_STRING="DefaultEndpointsProtocol=https;AccountName=telepathy;AccountKey=CIPIGmbH3eWA80UXsC/Fr18FOWINrSuyvAd6cQM0r3uzjU+FU5pEUEGpqGLFad4MgVuixlUGhFJggCvZv1Li3g==;EndpointSuffix=core.windows.net" sessionlauncher