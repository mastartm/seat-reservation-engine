FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY SeatReservation.sln ./
COPY src/SeatReservation.Domain/*.csproj src/SeatReservation.Domain/
COPY src/SeatReservation.Application/*.csproj src/SeatReservation.Application/
COPY src/SeatReservation.Infrastructure/*.csproj src/SeatReservation.Infrastructure/
COPY src/SeatReservation.Api/*.csproj src/SeatReservation.Api/
COPY tests/SeatReservation.Domain.Tests/*.csproj tests/SeatReservation.Domain.Tests/
RUN dotnet restore
COPY . .
RUN dotnet publish src/SeatReservation.Api -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "SeatReservation.Api.dll"]
