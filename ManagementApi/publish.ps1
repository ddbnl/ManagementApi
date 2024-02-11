docker build -t api-service -f ApiServiceDockerfile .
docker tag api-service cregcosmos.azurecr.io/api-service
docker push cregcosmos.azurecr.io/api-service 
Restart-AzContainerAppRevision -Name 'capp-cosmos--capp-cosmos-4' -ContainerAppName capp-cosmos -ResourceGroupName cosmos-rg     

docker build -t handler-service -f HandlerServiceDockerfile .
docker tag handler-service cregcosmos.azurecr.io/handler-service
docker push cregcosmos.azurecr.io/handler-service 
Restart-AzContainerAppRevision -Name 'cap-handler-cosmos--cap-handler-cosmos-5' -ContainerAppName cap-handler-cosmos -ResourceGroupName cosmos-rg     
