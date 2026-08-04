import { defineConfig } from "vite";
import { cabinetncOffsetApi } from "./vite.offset-api.js";

export default defineConfig({
  server: {
    port: 5177,
    open: false,
    watch: {
      // .NET builds lock tmp files under obj/; watching them crashes Vite (EBUSY).
      ignored: ["**/dotnet/**/bin/**", "**/dotnet/**/obj/**", "**/native/**/build/**"],
    },
  },
  plugins: [cabinetncOffsetApi()],
});
