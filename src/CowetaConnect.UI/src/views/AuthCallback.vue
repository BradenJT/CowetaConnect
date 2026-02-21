<!-- src/CowetaConnect.UI/src/views/AuthCallback.vue -->
<script setup lang="ts">
import { onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '../stores/auth'

const router = useRouter()
const auth   = useAuthStore()

onMounted(() => {
  const hash   = window.location.hash          // e.g. "#token=eyJ..."
  const params = new URLSearchParams(hash.substring(1))
  const token  = params.get('token')

  // Clear the token from the URL immediately — don't leave it in browser history.
  history.replaceState(null, '', window.location.pathname)

  if (token) {
    auth.setToken(token)
    router.replace({ name: 'home' })
  } else {
    // No token — something went wrong with the OAuth flow.
    router.replace({ name: 'login' })
  }
})
</script>

<template>
  <div>Signing you in…</div>
</template>
