FROM maven:3.9-eclipse-temurin-21 AS build

WORKDIR /app

COPY pom.xml ./
RUN mvn dependency:go-offline

COPY src ./src

RUN mvn clean package -DskipTests


FROM eclipse-temurin:21-jre

WORKDIR /app

ENV SERVER_PORT=3003

EXPOSE 3003

COPY --from=build \
  /app/target/alert-service-0.0.1-SNAPSHOT.jar \
  app.jar

ENTRYPOINT ["java", "-jar", "app.jar"]
