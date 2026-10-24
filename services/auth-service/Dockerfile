FROM node:24.15.0-alpine AS build

WORKDIR /app

COPY package*.json ./

RUN npm ci --legacy-peer-deps

COPY tsconfig.json ./
COPY tsconfig.build.json ./
COPY nest-cli.json ./
COPY src/ src/

RUN npm run build && npm prune --omit=dev --legacy-peer-deps


FROM node:24.15.0-alpine

WORKDIR /app

ENV NODE_ENV=production
ENV PORT=3001

COPY --from=build --chown=node:node /app/node_modules node_modules/
COPY --from=build --chown=node:node /app/dist dist/
COPY --chown=node:node package.json package.json

USER node

EXPOSE 3001

CMD ["sh", "-c", "node ./node_modules/typeorm/cli.js migration:run -d dist/database/data-source.js && node dist/main.js"]
