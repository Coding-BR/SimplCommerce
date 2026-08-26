FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY BlazorClient.csproj .
RUN dotnet restore BlazorClient.csproj -r browser-wasm
COPY . .
RUN dotnet publish BlazorClient.csproj -c Release -r browser-wasm --no-restore -o /app/publish

FROM nginx:1.27-alpine
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/publish/wwwroot /usr/share/nginx/html
EXPOSE 80
