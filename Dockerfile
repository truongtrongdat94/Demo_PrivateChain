FROM node:22-bookworm-slim AS contract-build
WORKDIR /src/contract

COPY contract/package*.json ./
RUN npm ci

COPY contract/hardhat.config.ts ./
COPY contract/contracts ./contracts
RUN npm run compile

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY HashAnchorDemo.csproj ./
RUN dotnet restore

COPY . .
COPY --from=contract-build /src/contract/artifacts ./contract/artifacts
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:5000
EXPOSE 5000

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "HashAnchorDemo.dll"]
