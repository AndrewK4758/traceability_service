FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

# The build context must be the parent directory so we can include shared_protos
COPY shared_protos/ ./shared_protos/
COPY traceability_service/ ./traceability_service/

WORKDIR /app/traceability_service
RUN dotnet restore
RUN dotnet publish -c Release -o /out

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /out .

EXPOSE 50052

ENTRYPOINT ["dotnet", "TraceabilityService.dll"]
