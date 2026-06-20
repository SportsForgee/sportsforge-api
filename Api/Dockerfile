FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 5186

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Api/Api.csproj Api/
COPY KhoiIntegration/khoiIntegration.csproj KhoiIntegration/
RUN dotnet restore Api/Api.csproj
COPY . .
RUN dotnet publish Api/Api.csproj -c Release -o /app/publish

FROM base
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Api.dll"]
