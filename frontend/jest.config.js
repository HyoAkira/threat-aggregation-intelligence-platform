export default {
  preset: 'ts-jest',
  testEnvironment: 'node',
  testMatch: ['<rootDir>/src/**/*.test.{js,jsx,ts,tsx}'],
  transform: {
    '^.+\\.tsx?$': ['ts-jest', { tsconfig: '<rootDir>/tsconfig.jest.json' }],
  },
  moduleNameMapper: {
    '^(\\.{1,2}/.*)\\.js$': '$1',
    '\\.css\\?url$': '<rootDir>/src/test/stubCssModule.ts',
    '\\.css$': '<rootDir>/src/test/stubCssModule.ts',
    '^@tanstack/react-devtools$': '<rootDir>/src/test/stubDevtools.tsx',
  },
}
