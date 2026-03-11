FROM nginx:1.27-alpine

COPY . /usr/share/nginx/html
COPY docker/nginx.conf /etc/nginx/conf.d/default.conf
COPY docker/entrypoint.sh /docker-entrypoint.d/40-intentify-env.sh

RUN chmod +x /docker-entrypoint.d/40-intentify-env.sh

EXPOSE 80
