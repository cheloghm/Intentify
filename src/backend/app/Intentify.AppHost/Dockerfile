FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .

# Find and restore the solution file wherever it is
RUN find . -name "Intentify.sln" | head -1 | xargs dotnet restore

# Publish the AppHost project
RUN find . -name "Intentify.AppHost.csproj" | head -1 | xargs -I{} dotnet publish {} -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:5000
EXPOSE 5000
ENTRYPOINT ["dotnet", "Intentify.AppHost.dll"]
