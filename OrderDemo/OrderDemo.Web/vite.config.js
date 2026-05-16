import { defineConfig, loadEnv } from 'vite'
import vue from '@vitejs/plugin-vue'

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '')

  const apiTarget = env['services__orderdemo-api__http__0']
    ?? 'http://localhost:5000'

  return {
    plugins: [vue()],
    server: {
      port: env.PORT ? parseInt(env.PORT, 10) : 5173,
      proxy: {
        '/api': {
          target: apiTarget,
          changeOrigin: true
        }
      }
    }
  }
})
