import { setupWorker } from 'msw/browser'
import { handlers } from './gerado/handlers'

export const worker = setupWorker(...handlers)
