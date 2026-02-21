<!-- src/CowetaConnect.UI/src/views/HomeView.vue -->
<script setup lang="ts">
import { useAuthStore } from '../stores/auth'
const auth = useAuthStore()
</script>

<template>
  <div>
    <h1>CowetaConnect</h1>
    <p v-if="auth.isAuthenticated">You are signed in.</p>
    <RouterLink v-else to="/login">Sign in</RouterLink>
  </div>
</template>
