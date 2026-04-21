# ── Stage 1: build ──────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Minedu.VC.VerificationPortal/Minedu.VC.VerificationPortal.csproj Minedu.VC.VerificationPortal/
RUN dotnet restore Minedu.VC.VerificationPortal/Minedu.VC.VerificationPortal.csproj

COPY Minedu.VC.VerificationPortal/ Minedu.VC.VerificationPortal/
RUN dotnet publish Minedu.VC.VerificationPortal/Minedu.VC.VerificationPortal.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ── Stage 2: runtime ────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

RUN mkdir -p /app/logs

COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "Minedu.VC.VerificationPortal.dll"]
