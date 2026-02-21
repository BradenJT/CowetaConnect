// src/CowetaConnect.UI/src/stores/auth.ts
import { defineStore } from 'pinia'
import { ref, computed } from 'vue'

export const useAuthStore = defineStore('auth', () => {
  // Access token stored in memory only — never in localStorage or sessionStorage.
  const token = ref<string | null>(null)

  const isAuthenticated = computed(() => token.value !== null)

  function setToken(jwt: string) {
    token.value = jwt
  }

  function logout() {
    token.value = null
  }

  return { token, isAuthenticated, setToken, logout }
})
