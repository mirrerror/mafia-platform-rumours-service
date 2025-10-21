# Stage 1: Base image for the final runtime environment
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

# Create a non-root user and group for better security
RUN addgroup --system --gid 1000 appgroup && \
    adduser --system --uid 1000 --ingroup appgroup appuser

RUN mkdir -p /app/logs && \
    chown -R appuser:appgroup /app/logs

# Now, switch to the non-root user
USER appuser

# Stage 2: Build the application
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy project files and restore dependencies first to leverage Docker layer caching
COPY ["MafiaRumoursService/MafiaRumoursService.csproj", "MafiaRumoursService/"]
COPY ["MafiaRumoursService.Tests/MafiaRumoursService.Tests.csproj", "MafiaRumoursService.Tests/"]
RUN dotnet restore "MafiaRumoursService/MafiaRumoursService.csproj"

# Copy the rest of the source code
COPY . .

# Stage 3: Run the unit tests
FROM build AS test
WORKDIR "/src/MafiaRumoursService.Tests"
RUN dotnet test --no-restore -c $BUILD_CONFIGURATION

# Stage 4: Publish the application, using the result of the test stage
# This ensures that publishing only happens if the tests pass.
FROM test AS publish
WORKDIR "/src/MafiaRumoursService"
RUN dotnet publish "MafiaRumoursService.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Stage 5: Create the final, lean image
FROM base AS final
WORKDIR /app
COPY --from=publish --chown=appuser:appgroup /app/publish .
ENTRYPOINT ["dotnet", "MafiaRumoursService.dll"]