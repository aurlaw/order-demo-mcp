<script setup>
import { ref, reactive, computed, onMounted, watch } from 'vue'

const token           = ref(localStorage.getItem('token') || null)
const isAuthenticated = computed(() => !!token.value)
const loginForm       = reactive({ username: '', password: '', loading: false, error: null })

const orders = ref([])
const loading = ref(false)
const error = ref(null)

const pagination = reactive({ page: 1, pageSize: 20, totalCount: 0, totalPages: 0 })
const filters = reactive({ lastName: '', from: '', to: '' })
const applied = reactive({ lastName: '', from: '', to: '' })

const darkMode = ref(
  localStorage.getItem('darkMode') === 'true' ||
  (!localStorage.getItem('darkMode') && window.matchMedia('(prefers-color-scheme: dark)').matches)
)

watch(darkMode, val => {
  document.documentElement.classList.toggle('dark', val)
  localStorage.setItem('darkMode', String(val))
}, { immediate: true })

function toggleDark() {
  darkMode.value = !darkMode.value
}

async function fetchOrders() {
  loading.value = true
  error.value = null
  try {
    const params = new URLSearchParams()
    params.set('page', pagination.page)
    params.set('pageSize', pagination.pageSize)
    if (applied.lastName) params.set('lastName', applied.lastName)
    if (applied.from) params.set('from', applied.from)
    if (applied.to) params.set('to', applied.to)

    const res = await fetch(`/api/orders?${params}`, {
      headers: { Authorization: `Bearer ${token.value}` }
    })
    if (res.status === 401) { logout(); return }
    if (!res.ok) throw new Error(`HTTP ${res.status}`)
    const data = await res.json()

    orders.value = data.items
    pagination.totalCount = data.totalCount
    pagination.totalPages = data.totalPages
  } catch (e) {
    error.value = e.message
  } finally {
    loading.value = false
  }
}

async function login() {
  loginForm.loading = true
  loginForm.error   = null
  try {
    const res = await fetch('/api/auth/login', {
      method:  'POST',
      headers: { 'Content-Type': 'application/json' },
      body:    JSON.stringify({ username: loginForm.username, password: loginForm.password })
    })
    if (!res.ok) throw new Error('Invalid username or password')
    const data   = await res.json()
    token.value  = data.token
    localStorage.setItem('token', data.token)
    fetchOrders()
  } catch (e) {
    loginForm.error = e.message
  } finally {
    loginForm.loading = false
  }
}

function logout() {
  token.value = null
  localStorage.removeItem('token')
  orders.value = []
}

function applyFilters() {
  applied.lastName = filters.lastName
  applied.from = filters.from
  applied.to = filters.to
  pagination.page = 1
  fetchOrders()
}

function clearFilters() {
  filters.lastName = ''
  filters.from = ''
  filters.to = ''
  applied.lastName = ''
  applied.from = ''
  applied.to = ''
  pagination.page = 1
  fetchOrders()
}

function goToPage(page) {
  if (page < 1 || page > pagination.totalPages) return
  pagination.page = page
  fetchOrders()
}

function formatDate(str) {
  return new Date(str).toLocaleDateString('en-US', { year: 'numeric', month: 'short', day: 'numeric' })
}

function formatCurrency(n) {
  return new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(n)
}

function pageWindow() {
  const { page, totalPages } = pagination
  const pages = []
  const delta = 2
  const left = Math.max(1, page - delta)
  const right = Math.min(totalPages, page + delta)

  if (left > 1) {
    pages.push(1)
    if (left > 2) pages.push('...')
  }
  for (let i = left; i <= right; i++) pages.push(i)
  if (right < totalPages) {
    if (right < totalPages - 1) pages.push('...')
    pages.push(totalPages)
  }
  return pages
}

onMounted(() => { if (isAuthenticated.value) fetchOrders() })
</script>

<template>
  <div v-if="!isAuthenticated" class="login-wrapper">
    <div class="login-card">
      <h1>Order Demo</h1>
      <p class="login-subtitle">Sign in to continue</p>
      <form @submit.prevent="login">
        <div class="field">
          <label>Username</label>
          <input v-model="loginForm.username" type="text" autocomplete="username" required />
        </div>
        <div class="field">
          <label>Password</label>
          <input v-model="loginForm.password" type="password" autocomplete="current-password" required />
        </div>
        <p v-if="loginForm.error" class="login-error">{{ loginForm.error }}</p>
        <button type="submit" class="btn-primary full-width" :disabled="loginForm.loading">
          {{ loginForm.loading ? 'Signing in…' : 'Sign In' }}
        </button>
      </form>
    </div>
  </div>

  <template v-if="isAuthenticated">
    <header>
      <div class="header-inner">
        <h1>Order Dashboard</h1>
        <div class="header-right">
          <span v-if="!loading" class="badge">{{ pagination.totalCount.toLocaleString() }} orders</span>
          <button class="btn-ghost theme-toggle" @click="toggleDark" :title="darkMode ? 'Switch to light mode' : 'Switch to dark mode'">
            <svg v-if="darkMode" width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <circle cx="12" cy="12" r="5"/>
              <line x1="12" y1="1" x2="12" y2="3"/><line x1="12" y1="21" x2="12" y2="23"/>
              <line x1="4.22" y1="4.22" x2="5.64" y2="5.64"/><line x1="18.36" y1="18.36" x2="19.78" y2="19.78"/>
              <line x1="1" y1="12" x2="3" y2="12"/><line x1="21" y1="12" x2="23" y2="12"/>
              <line x1="4.22" y1="19.78" x2="5.64" y2="18.36"/><line x1="18.36" y1="5.64" x2="19.78" y2="4.22"/>
            </svg>
            <svg v-else width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <path d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z"/>
            </svg>
          </button>
          <button class="btn-ghost" @click="logout">Sign Out</button>
        </div>
      </div>
    </header>

    <main>
      <div class="card filter-bar">
        <input v-model="filters.lastName" type="text" placeholder="Last name…" />
        <input v-model="filters.from" type="date" />
        <input v-model="filters.to" type="date" />
        <button class="btn-primary" @click="applyFilters">Apply</button>
        <button class="btn-secondary" @click="clearFilters">Clear</button>
      </div>

      <div class="card">
        <div v-if="loading" class="state-msg">Loading…</div>
        <div v-else-if="error" class="state-msg error">Error: {{ error }}</div>
        <div v-else>
          <div class="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Order #</th>
                  <th>Date</th>
                  <th>Customer</th>
                  <th>Email</th>
                  <th>Items</th>
                  <th class="right">Total</th>
                </tr>
              </thead>
              <tbody>
                <tr v-if="orders.length === 0">
                  <td colspan="6" class="empty">No orders found.</td>
                </tr>
                <tr v-for="order in orders" :key="order.id">
                  <td class="mono">{{ order.orderNumber }}</td>
                  <td>{{ formatDate(order.orderDate) }}</td>
                  <td>{{ order.customer.firstName }} {{ order.customer.lastName }}</td>
                  <td class="muted">{{ order.customer.email }}</td>
                  <td><span class="pill">{{ order.lines.length }}</span></td>
                  <td class="right bold tabular">{{ formatCurrency(order.total) }}</td>
                </tr>
              </tbody>
            </table>
          </div>

          <div class="pagination">
            <button :disabled="pagination.page <= 1" @click="goToPage(pagination.page - 1)">‹</button>
            <template v-for="p in pageWindow()" :key="p">
              <span v-if="p === '...'" class="ellipsis">…</span>
              <button v-else :class="{ active: p === pagination.page }" @click="goToPage(p)">{{ p }}</button>
            </template>
            <button :disabled="pagination.page >= pagination.totalPages" @click="goToPage(pagination.page + 1)">›</button>
            <span class="page-label">Page {{ pagination.page }} of {{ pagination.totalPages }}</span>
          </div>
        </div>
      </div>
    </main>
  </template>
</template>

<style scoped>
header {
  padding: 24px 32px 0;
}

.header-inner {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

header h1 {
  font-size: 1.5rem;
  font-weight: 700;
  color: var(--text);
}

.header-right {
  display: flex;
  align-items: center;
  gap: 12px;
}

.badge {
  background: var(--badge-bg);
  color: var(--badge-text);
  font-size: 0.75rem;
  font-weight: 600;
  padding: 3px 10px;
  border-radius: 999px;
}

main {
  padding: 20px 32px 40px;
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.card {
  background: var(--surface);
  border-radius: 10px;
  box-shadow: var(--card-shadow);
  padding: 20px;
}

.filter-bar {
  display: flex;
  gap: 10px;
  align-items: center;
  flex-wrap: wrap;
}

.filter-bar input {
  border: 1px solid var(--input-border);
  border-radius: 6px;
  padding: 7px 12px;
  font-size: 0.875rem;
  color: var(--text);
  background: var(--input-bg);
  outline: none;
}

.filter-bar input:focus {
  border-color: var(--accent);
  box-shadow: 0 0 0 2px rgba(99, 102, 241, 0.15);
}

.btn-primary, .btn-secondary {
  padding: 7px 18px;
  border-radius: 6px;
  font-size: 0.875rem;
  font-weight: 500;
  cursor: pointer;
  border: none;
}

.btn-primary {
  background: var(--accent);
  color: #fff;
}

.btn-primary:hover {
  background: var(--accent-hover);
}

.btn-primary:disabled {
  opacity: 0.6;
  cursor: default;
}

.btn-secondary {
  background: var(--btn-secondary-bg);
  color: var(--text-label);
  border: 1px solid var(--input-border);
}

.btn-secondary:hover {
  background: var(--btn-secondary-hover);
}

.btn-ghost {
  background: none;
  border: 1px solid var(--input-border);
  border-radius: 6px;
  padding: 6px 14px;
  font-size: 0.875rem;
  color: var(--text-label);
  cursor: pointer;
}

.btn-ghost:hover {
  background: var(--btn-ghost-hover);
}

.theme-toggle {
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 6px 10px;
}

.state-msg {
  text-align: center;
  padding: 40px;
  color: var(--text-muted);
}

.state-msg.error {
  color: #ef4444;
}

.table-wrap {
  overflow-x: auto;
}

table {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.875rem;
}

thead th {
  text-align: left;
  padding: 10px 14px;
  font-weight: 600;
  font-size: 0.75rem;
  text-transform: uppercase;
  letter-spacing: 0.04em;
  color: var(--text-muted);
  border-bottom: 1px solid var(--border);
}

tbody tr {
  border-bottom: 1px solid var(--border-subtle);
  transition: background 0.1s;
}

tbody tr:hover {
  background: var(--row-hover);
}

tbody td {
  padding: 11px 14px;
  color: var(--text-secondary);
  vertical-align: middle;
}

.mono {
  font-family: ui-monospace, Consolas, monospace;
  font-size: 0.8125rem;
}

.muted {
  color: var(--text-faint);
}

.right {
  text-align: right;
}

.bold {
  font-weight: 600;
}

.tabular {
  font-variant-numeric: tabular-nums;
}

.pill {
  display: inline-block;
  background: var(--pill-bg);
  color: var(--pill-text);
  font-size: 0.75rem;
  font-weight: 600;
  padding: 2px 8px;
  border-radius: 999px;
}

.empty {
  text-align: center;
  padding: 40px;
  color: var(--text-faint);
}

.pagination {
  display: flex;
  align-items: center;
  gap: 4px;
  padding-top: 16px;
  flex-wrap: wrap;
}

.pagination button {
  min-width: 34px;
  height: 34px;
  border-radius: 6px;
  border: 1px solid var(--border);
  background: var(--pagination-bg);
  color: var(--text-secondary);
  font-size: 0.875rem;
  cursor: pointer;
  padding: 0 8px;
}

.pagination button:hover:not(:disabled):not(.active) {
  background: var(--pagination-hover);
}

.pagination button.active {
  background: var(--accent);
  color: #fff;
  border-color: var(--accent);
  font-weight: 600;
}

.pagination button:disabled {
  opacity: 0.4;
  cursor: default;
}

.ellipsis {
  color: var(--text-faint);
  padding: 0 4px;
}

.page-label {
  margin-left: 8px;
  font-size: 0.8125rem;
  color: var(--text-muted);
}

/* Login */
.login-wrapper {
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
}

.login-card {
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: 12px;
  padding: 40px;
  width: 100%;
  max-width: 380px;
}

.login-card h1 {
  font-size: 22px;
  font-weight: 700;
  color: var(--text);
  margin-bottom: 4px;
}

.login-subtitle {
  color: var(--text-muted);
  font-size: 14px;
  margin-bottom: 28px;
}

.field {
  display: flex;
  flex-direction: column;
  gap: 4px;
  margin-bottom: 16px;
}

.field label {
  font-size: 12px;
  font-weight: 500;
  color: var(--text-muted);
  text-transform: uppercase;
  letter-spacing: 0.05em;
}

.field input {
  height: 40px;
  padding: 0 12px;
  border: 1px solid var(--input-border);
  border-radius: 6px;
  font-size: 14px;
  color: var(--text);
  background: var(--input-bg);
  outline: none;
  transition: border-color 0.15s;
}

.field input:focus {
  border-color: var(--accent);
  box-shadow: 0 0 0 3px rgba(99, 102, 241, 0.12);
}

.login-error {
  font-size: 13px;
  color: #dc2626;
  margin-bottom: 12px;
}

.full-width {
  width: 100%;
  margin-top: 4px;
}
</style>
