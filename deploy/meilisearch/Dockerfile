FROM getmeili/meilisearch:v1.15

ENV MEILI_ENV=production
ENV MEILI_NO_ANALYTICS=true

CMD sh -c 'meilisearch --http-addr 0.0.0.0:${PORT:-7700}'
