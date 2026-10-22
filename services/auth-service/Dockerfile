FROM node:24.15.0-alpine AS build
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY tsconfig.json ./
COPY src/ src/
RUN npm run build && npm prune --omit=dev

FROM node:24.15.0-alpine
WORKDIR /app
ENV NODE_ENV=production PORT=3000
COPY --from=build --chown=node:node /app/node_modules node_modules/
COPY --from=build --chown=node:node /app/dist dist/
COPY --chown=node:node migrations/ migrations/
COPY --chown=node:node package.json ./
USER node
EXPOSE 3000
CMD ["node", "dist/src/main.js"]
