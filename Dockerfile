# Stage 1: Build & Publish
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project file and restore dependencies (restore is cached as its own layer)
COPY *.csproj ./
RUN dotnet restore

# Copy all source files and publish release build
COPY . ./
RUN dotnet publish -c Release --no-restore -o /app/publish

# Stage 2: Runtime Container
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Install Arial-metric-compatible fonts (Liberation Sans) so PDF export works.
# PDFsharp's WindowsFontResolver looks for arial.ttf / arialbd.ttf / ariali.ttf /
# arialbi.ttf in the Windows fonts folder or in the app base directory. On Linux
# the Windows folder does not exist, so we copy Liberation Sans under those exact
# file names into /app, which is the resolver's local fallback directory.
RUN apt-get update \
    && apt-get install -y --no-install-recommends fonts-liberation \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

# Entrypoint waits for SQL Server before starting the app (first-boot safety).
COPY entrypoint.sh ./
RUN chmod +x entrypoint.sh \
    && cp /usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf    /app/arial.ttf \
    && cp /usr/share/fonts/truetype/liberation/LiberationSans-Bold.ttf       /app/arialbd.ttf \
    && cp /usr/share/fonts/truetype/liberation/LiberationSans-Italic.ttf     /app/ariali.ttf \
    && cp /usr/share/fonts/truetype/liberation/LiberationSans-BoldItalic.ttf /app/arialbi.ttf

# Expose internal container port
EXPOSE 80
ENV ASPNETCORE_URLS=http://+:80

# Run compiled DLL (after the DB wait in entrypoint.sh)
ENTRYPOINT ["./entrypoint.sh"]
