FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/ClinicDesk.Api/ClinicDesk.Api.csproj src/ClinicDesk.Api/
RUN dotnet restore src/ClinicDesk.Api/ClinicDesk.Api.csproj

COPY src/ClinicDesk.Api/ src/ClinicDesk.Api/
RUN dotnet publish src/ClinicDesk.Api/ClinicDesk.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app .

ENTRYPOINT ["dotnet", "ClinicDesk.Api.dll"]
