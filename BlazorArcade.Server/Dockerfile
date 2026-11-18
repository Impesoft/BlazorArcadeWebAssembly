# Build from the repository root:
#   docker build -f BlazorArcade.Server/Dockerfile -t blazorarcade-server .
#   docker run -p 8080:8080 -v arcade-data:/app/data blazorarcade-server
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY BlazorArcade.Shared/BlazorArcade.Shared.csproj BlazorArcade.Shared/
COPY BlazorArcade.Server/BlazorArcade.Server.csproj BlazorArcade.Server/
RUN dotnet restore BlazorArcade.Server/BlazorArcade.Server.csproj
COPY BlazorArcade.Shared/ BlazorArcade.Shared/
COPY BlazorArcade.Server/ BlazorArcade.Server/
RUN dotnet publish BlazorArcade.Server/BlazorArcade.Server.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
# Scores live in /app/data; mount a volume there so they survive redeploys.
RUN mkdir -p /app/data && chown -R app:app /app/data
USER app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "BlazorArcade.Server.dll"]
