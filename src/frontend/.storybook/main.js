// eslint-disable-next-line no-undef
module.exports = {
	staticDirs: ['../public'],
	stories: ['../src/**/*.stories.mdx', '../src/**/*.stories.@(js|jsx|ts|tsx)'],
	addons: [
		'@storybook/addon-essentials',
		'@storybook/addon-links',
		'@storybook/addon-a11y',
	],
	framework: {
		name: '@storybook/react-vite',
		options: {},
	},
	docs: {
		autodocs: true,
	},
	typescript: {
		reactDocgen: 'react-docgen',
	},
};
