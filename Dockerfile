FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

COPY --from=shared_protos . /app/shared_protos
COPY --from=mployee_sdk . /app/Mployee.Dotnet.Sdk
COPY . /app/traceability_service

WORKDIR /app/traceability_service
RUN dotnet restore
RUN dotnet publish TraceabilityService/TraceabilityService.csproj -c Release -o /out

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /out .

EXPOSE 50053

ENTRYPOINT ["dotnet", "TraceabilityService.dll"]
