// Jest stub for Vite `?url` stylesheet imports so route modules can be imported
// in the Node test environment. The placeholder href keeps React's stylesheet
// handling happy during server rendering in tests.
export default '/stub.css'
