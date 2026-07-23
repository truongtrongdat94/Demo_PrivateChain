FROM nginx:1.28-alpine

COPY nginx.conf /etc/nginx/nginx.conf
COPY index.html app.css app.js /usr/share/nginx/html/

USER nginx
EXPOSE 8080
