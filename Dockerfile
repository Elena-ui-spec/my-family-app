# Use the official .NET SDK image as the build environment
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build

# Set the working directory inside the container
WORKDIR /app

# Copy the .csproj file and restore any dependencies
COPY *.csproj ./
RUN dotnet restore

# Copy the entire project and build it
COPY . ./
RUN dotnet publish -c Release -o out

# Use the official .NET runtime image for the final image
FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS runtime

# Set the working directory in the runtime image
WORKDIR /app

# Copy the build output from the build stage
COPY --from=build /app/out .

# Set the environment variable for ASP.NET Core
ENV ASPNETCORE_ENVIRONMENT=Production

# Expose the port the app runs on
EXPOSE 7029

# Start the application
ENTRYPOINT ["dotnet", "FamilyApp.API.dll"]
