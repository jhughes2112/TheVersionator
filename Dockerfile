# Stage 1: Build the application
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
COPY . /source
WORKDIR /source
RUN dotnet publish --nologo -c Release --self-contained=false -o /output /maxcpucount:1 

# Stage 2: Create the runtime image
FROM mcr.microsoft.com/dotnet/runtime:8.0-alpine AS runtime
WORKDIR /app
COPY --from=build /output /app
RUN  adduser --disabled-password --home /app --gecos '' noprivileges && chown -R noprivileges /app && ls -al /app
USER noprivileges
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1
ENTRYPOINT ["./TheVersionator"]