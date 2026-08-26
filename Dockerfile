FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY IdealCreative.Api/IdealCreative.Api.csproj IdealCreative.Api/
RUN dotnet restore IdealCreative.Api/IdealCreative.Api.csproj
COPY IdealCreative.Api/ IdealCreative.Api/
RUN dotnet publish IdealCreative.Api/IdealCreative.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /home/app/.aspnet/DataProtection-Keys \
    && chown -R "$APP_UID:$APP_UID" /home/app/.aspnet
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "IdealCreative.Api.dll"]
