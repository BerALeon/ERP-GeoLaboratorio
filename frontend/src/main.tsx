import { ConfigProvider, App as AntApp } from 'antd'
import esES from 'antd/locale/es_ES'
import dayjs from 'dayjs'
import 'dayjs/locale/es'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import React from 'react'
import ReactDOM from 'react-dom/client'
import { BrowserRouter } from 'react-router-dom'
import App from './App'
import { AuthProvider } from './auth/AuthContext'
import { theme } from './theme'
import './index.css'

dayjs.locale('es')
const queryClient = new QueryClient({ defaultOptions: { queries: { refetchOnWindowFocus: false, retry: 1 } } })

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <ConfigProvider locale={esES} theme={theme}>
      <AntApp>
        <QueryClientProvider client={queryClient}>
          <BrowserRouter>
            <AuthProvider>
              <App />
            </AuthProvider>
          </BrowserRouter>
        </QueryClientProvider>
      </AntApp>
    </ConfigProvider>
  </React.StrictMode>,
)
