docker tag sessionlauncher telepathy.azurecr.io/telepathy/sessionlauncher
docker tag brokerlauncher  telepathy.azurecr.io/telepathy/brokerlauncher
docker tag brokerworker  telepathy.azurecr.io/telepathy/brokerworker
docker push telepathy.azurecr.io/telepathy/sessionlauncher
docker push telepathy.azurecr.io/telepathy/brokerlauncher
docker push telepathy.azurecr.io/telepathy/brokerworker
kubectl apply -f src/Telepathy.yml
:: kubectl exec session-launcher-7649677664-kp45f -i -t -- bash -il --namespace=telepathy
:: kubectl get pod session-launcher-7649677664-kp45f --namespace=telepathy
:: kubectl exec brokerworkerdfe6d000-e8d3-4814-b4cf-fcaf05932151-0 -i -t powershell.exe --namespace=telepathy
:: az aks browse --resource-group vnet-japaneast-azurebatch --name HATelepathy
:: az ad sp create-for-rbac -n "MyHATelepathy" --role contributor --scopes /subscriptions/01b6a57a-5aef-40e2-8af7-562a2f81462e/resourceGroups/vnet-japaneast-azurebatch
