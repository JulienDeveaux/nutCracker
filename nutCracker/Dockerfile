FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["./nutCracker.csproj", "./"]
RUN dotnet restore "./nutCracker.csproj"
COPY . .
RUN dotnet build "./nutCracker.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "./nutCracker.csproj" -c Release -o /app/publish

FROM base AS final

RUN apt-get update && \
     apt-get install \
        ca-certificates \
        curl \
        gnupg \
        lsb-release -y && \
    mkdir -p /etc/apt/keyrings && \
    curl -fsSL https://download.docker.com/linux/ubuntu/gpg | gpg --dearmor -o /etc/apt/keyrings/docker.gpg && \
    echo \
      "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/ubuntu \
      bionic stable" | tee /etc/apt/sources.list.d/docker.list > /dev/null && \
    apt-get update && \
    apt-get install docker-ce docker-ce-cli containerd.io docker-compose-plugin -y

WORKDIR /app
COPY --from=publish /app/publish .

VOLUME /var/run/docker.sock

ENTRYPOINT ["dotnet", "nutCracker.dll"]
