import axios from "axios";
import { useAuthStore } from "@/stores/authStore";

const apiClient = axios.create({
  baseURL: process.env.NEXT_PUBLIC_API_URL ?? "/api",
});

apiClient.interceptors.request.use((config) => {
  const tenantId = useAuthStore.getState().tenantId;
  if (tenantId !== null) {
    config.headers["X-Tenant-ID"] = tenantId;
  }
  return config;
});

apiClient.interceptors.request.use((config) => {
  // Token lives in the "access_token" cookie set by the login page.
  // When Keycloak replaces dev auth (see middleware.ts TODO), this reads the real JWT.
  const token =
    typeof document !== "undefined"
      ? document.cookie
          .split("; ")
          .find((row) => row.startsWith("access_token="))
          ?.split("=")[1]
      : null;
  if (token && token !== "dev") {
    config.headers["Authorization"] = `Bearer ${token}`;
  }
  return config;
});

export default apiClient;
