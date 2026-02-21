<!-- src/CowetaConnect.UI/src/views/LoginView.vue -->
<script setup lang="ts">
const apiBase = import.meta.env.VITE_API_BASE_URL ?? ''

function signInWithGoogle() {
  // Full-page navigation — the OAuth flow requires a real redirect, not fetch().
  window.location.href = `${apiBase}/api/v1/auth/google`
}
</script>

<template>
  <div class="login-page">
    <h1>Sign in to CowetaConnect</h1>

    <button class="google-btn" @click="signInWithGoogle">
      <img
        src="https://www.gstatic.com/firebasejs/ui/2.0.0/images/auth/google.svg"
        alt=""
        width="18"
        height="18"
      />
      Sign in with Google
    </button>
  </div>
</template>

<style scoped>
.login-page {
  display: flex;
  flex-direction: column;
  align-items: center;
  padding: 4rem 1rem;
  gap: 1.5rem;
}

.google-btn {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  padding: 0.625rem 1.25rem;
  border: 1px solid #dadce0;
  border-radius: 4px;
  background: #fff;
  color: #3c4043;
  font-size: 0.9375rem;
  font-weight: 500;
  cursor: pointer;
  transition: background 0.15s;
}

.google-btn:hover {
  background: #f8f8f8;
}
</style>
