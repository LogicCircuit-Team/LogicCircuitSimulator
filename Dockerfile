FROM ubuntu:24.04 AS yosys
ARG OSS_CAD_SUITE_VERSION=2026-10-20
RUN apt-get update && apt-get install -y --no-install-recommends ca-certificates curl && rm -rf /var/lib/apt/lists/*
RUN suite_date=$(printf '%s' "$OSS_CAD_SUITE_VERSION" | tr -d '-') && curl -fsSL "https://github.com/YosysHQ/oss-cad-suite-build/releases/download/${OSS_CAD_SUITE_VERSION}/oss-cad-suite-linux-x64-${suite_date}.tgz" | tar -xz -C /opt
RUN printf 'module check(input a,input b,output y);assign y=a&b;endmodule' > /tmp/check.sv && /opt/oss-cad-suite/bin/yosys -Q -p 'read_slang /tmp/check.sv; hierarchy -top check; proc; flatten; opt; techmap; opt; write_json /tmp/check.json' && test -s /tmp/check.json

FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build
WORKDIR /source
COPY LogicCore/LogicCore.csproj LogicCore/
COPY BlazorApp1/BlazorApp1.csproj BlazorApp1/
COPY CircuitApi/CircuitApi.csproj CircuitApi/
RUN dotnet restore CircuitApi/CircuitApi.csproj
COPY . .
RUN dotnet publish CircuitApi/CircuitApi.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble
WORKDIR /app
COPY --from=yosys /opt/oss-cad-suite /opt/oss-cad-suite
COPY --from=build /app/publish .
ENV ASPNETCORE_HTTP_PORTS=8080
ENV ASPNETCORE_FORWARDEDHEADERS_ENABLED=true
ENV HdlSynthesis__YosysExecutable=/opt/oss-cad-suite/bin/yosys
ENV HdlSynthesis__EnvironmentScript=""
ENV DataProtection__KeyPath=/home/data-protection-keys
RUN mkdir -p /home/data-protection-keys && chown -R $APP_UID:$APP_UID /home /app
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "CircuitApi.dll"]
