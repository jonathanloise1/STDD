import globals from 'globals';
import pluginJs from '@eslint/js';
import tseslint from 'typescript-eslint';
import pluginReact from 'eslint-plugin-react';
import reactHooks from 'eslint-plugin-react-hooks';
import prettier from 'eslint-plugin-prettier';
import pluginImport from 'eslint-plugin-import';
import pluginJsxA11y from 'eslint-plugin-jsx-a11y';
import { fixupPluginRules } from '@eslint/compat';

export default [
	{
		settings: {
			react: {
				version: 'detect',
			},
		},
		files: ['**/*.{js,mjs,cjs,ts,jsx,tsx}'],
		...pluginReact.configs.flat.recommended,
		languageOptions: {
			...pluginReact.configs.flat.recommended.languageOptions,
			globals: globals.browser,
		},
		plugins: {
			react: pluginReact,
			'react-hooks': fixupPluginRules(reactHooks),
			import: pluginImport,
			'jsx-a11y': pluginJsxA11y,
			'@typescript-eslint': tseslint.plugin,
			prettier,
		},
		rules: {
			...reactHooks.configs.recommended.rules,
			'no-use-before-define': 'off',
			'no-case-declarations': 'off',
			'no-underscore-dangle': 'off',
			'react/jsx-filename-extension': [1, { extensions: ['.js', '.jsx', '.ts', '.tsx'] }],
			'no-param-reassign': 'off',
			'no-empty': 'warn',
			'import/no-extraneous-dependencies': 'warn',
			'react/jsx-props-no-spreading': [
				'warn',
				{
					html: 'ignore',
					custom: 'enforce',
					explicitSpread: 'enforce',
					exceptions: [
						'DropdownItem',
						'Element',
						'FullCalendar',
						'Leaf',
						'NavItem',
						'ReactSelect',
						'CreatableSelect',
						'Route',
						'Table',
						'Td',
						'Th',
						'ThResizer',
					],
				},
			],
			'react/no-unescaped-entities': ['error', { forbid: ['>', '}'] }],
			'jsx-a11y/label-has-associated-control': [
				'warn',
				{
					depth: 3,
				},
			],
			'react-hooks/exhaustive-deps': 'error',
			'react-hooks/rules-of-hooks': 'error',
			'react/function-component-definition': [
				2,
				{
					namedComponents: 'arrow-function',
					unnamedComponents: 'arrow-function',
				},
			],
			'react/no-arrow-function-lifecycle': 'off',
			'react/no-invalid-html-attribute': 'off',
			'react/no-unused-class-component-methods': 'off',
			'import/no-anonymous-default-export': [
				'error',
				{
					allowArray: true,
					allowArrowFunction: false,
					allowAnonymousClass: false,
					allowAnonymousFunction: false,
					allowCallExpression: true, // The true value here is for backward compatibility
					allowLiteral: false,
					allowObject: true,
				},
			],
			'arrow-body-style': ['off'],
			'react/jsx-key': ['error'],
			'prettier/prettier': ['error'],
			'react/require-default-props': ['off'],
		},
	},
	{
		ignores: [
			'**/temp.js',
			'config/*',
			'node_modules/',
			'build/',
			'src/components/icon/',
			'src/assets',
			'**.config.*',
		],
	},
	pluginJs.configs.recommended,
	...tseslint.configs.recommended,
	pluginReact.configs.flat.recommended,
	{
		rules: {
			'@typescript-eslint/no-explicit-any': 'off',
			'@typescript-eslint/ban-ts-comment': 'off',
			'@typescript-eslint/ban-ts-ignore': 'off',
			'template-curly-spacing': ['error', 'never'],
			'react/jsx-curly-brace-presence': ['error', { props: 'never', children: 'never' }],
			'react/self-closing-comp': [
				'error',
				{
					component: true,
					html: true,
				},
			],
			'prefer-template': 'error',
		},
	},
];
