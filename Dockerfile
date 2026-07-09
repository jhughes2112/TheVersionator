# Stage 1: Build a self-contained Native AOT linux-musl executable
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
RUN apk add --no-cache clang build-base zlib-dev
COPY . /source
WORKDIR /source
RUN dotnet publish TheVersionator.csproj --nologo -c Release -r linux-musl-x64 -o /output /maxcpucount:1

# Stage 2: Create the runtime image (no .NET runtime needed, just runtime deps)
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-alpine AS runtime
WORKDIR /app
COPY --from=build /output/TheVersionator /app/TheVersionator
RUN  adduser --disabled-password --home /app --gecos '' noprivileges && chown -R noprivileges /app && ls -al /app
USER noprivileges
ENTRYPOINT ["./TheVersionator"]
