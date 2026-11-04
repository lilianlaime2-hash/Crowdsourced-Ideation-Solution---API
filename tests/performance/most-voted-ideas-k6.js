import { performanceOptions } from './helpers/config.js';
import { setupRead, read } from './helpers/api.js';

export const options = performanceOptions(__ENV, ['get_most_voted']);
export function setup() { return setupRead('get_most_voted'); }
export default function (session) { read('get_most_voted', session); }
