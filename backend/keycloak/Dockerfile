FROM quay.io/keycloak/keycloak:24.0.5

USER root

COPY realm-export.json /opt/keycloak/realm-export.template.json
COPY docker-entrypoint.sh /opt/keycloak/docker-entrypoint.sh
RUN chmod +x /opt/keycloak/docker-entrypoint.sh

ENTRYPOINT ["/opt/keycloak/docker-entrypoint.sh"]
