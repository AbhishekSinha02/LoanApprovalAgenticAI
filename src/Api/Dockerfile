FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/Shared/LoanApproval.Shared.csproj", "src/Shared/"]
COPY ["src/Agents/LoanApproval.Agents.csproj", "src/Agents/"]
COPY ["src/Api/LoanApproval.Api.csproj", "src/Api/"]
RUN dotnet restore "src/Api/LoanApproval.Api.csproj"

COPY src/ src/
RUN dotnet publish "src/Api/LoanApproval.Api.csproj" -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "LoanApproval.Api.dll"]
