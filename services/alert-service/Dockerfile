FROM maven:3.9-eclipse-temurin-21 AS build
WORKDIR /app
COPY pom.xml .
RUN mvn -B dependency:go-offline
COPY src src
RUN mvn -B verify
FROM eclipse-temurin:21-jre
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/* && useradd --uid 10001 --create-home alert
WORKDIR /app
COPY --from=build /app/target/alert-service-0.0.1-SNAPSHOT.jar app.jar
USER alert
ENTRYPOINT ["java", "-jar", "app.jar"]
