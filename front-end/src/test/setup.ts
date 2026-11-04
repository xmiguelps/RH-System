import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { afterAll, afterEach, beforeAll } from 'vitest'
import { servidor } from '../shared/mocks/servidor'

// O jsdom 30.1.3 não tem estes métodos; o Base UI e o userEvent os chamam.
if (typeof window !== 'undefined') {
  HTMLElement.prototype.hasPointerCapture = () => false
  HTMLElement.prototype.setPointerCapture = () => {}
  HTMLElement.prototype.releasePointerCapture = () => {}
  HTMLElement.prototype.scrollIntoView = () => {}
}

beforeAll(() => servidor.listen({ onUnhandledFrame: 'error' }))

afterEach(() => {
  if (typeof document !== 'undefined') cleanup()
  servidor.resetHandlers()
})

afterAll(() => servidor.close())
