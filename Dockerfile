FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY UTB.Library.Db/UTB.Library.Db.csproj UTB.Library.Db/
COPY UTB.Library.ServiceDefaults/UTB.Library.ServiceDefaults.csproj UTB.Library.ServiceDefaults/
COPY UTB.Library.DbManager/UTB.Library.DbManager.csproj UTB.Library.DbManager/
RUN dotnet restore UTB.Library.DbManager/UTB.Library.DbManager.csproj

COPY UTB.Library.Db/ UTB.Library.Db/
COPY UTB.Library.ServiceDefaults/ UTB.Library.ServiceDefaults/
COPY UTB.Library.DbManager/ UTB.Library.DbManager/
RUN dotnet publish UTB.Library.DbManager/UTB.Library.DbManager.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "UTB.Library.DbManager.dll"]
