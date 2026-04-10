FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY UTB.Minute.Db/UTB.Minute.Db.csproj UTB.Minute.Db/
COPY UTB.Minute.ServiceDefaults/UTB.Minute.ServiceDefaults.csproj UTB.Minute.ServiceDefaults/
COPY UTB.Minute.DbManager/UTB.Minute.DbManager.csproj UTB.Minute.DbManager/
RUN dotnet restore UTB.Minute.DbManager/UTB.Minute.DbManager.csproj

COPY UTB.Minute.Db/ UTB.Minute.Db/
COPY UTB.Minute.ServiceDefaults/ UTB.Minute.ServiceDefaults/
COPY UTB.Minute.DbManager/ UTB.Minute.DbManager/
RUN dotnet publish UTB.Minute.DbManager/UTB.Minute.DbManager.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "UTB.Minute.DbManager.dll"]
