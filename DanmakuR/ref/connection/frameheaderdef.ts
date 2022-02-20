// frame header fields

import * as constants from 'constants'

export interface HeaderField {
	name: string;
	key: string;
	bytes: number;
	offset: number;
	value: number;
}


export const Fields: HeaderField[]=  [{
	name: 'Header Length',
	key: 'headerLen',
	bytes: 2,
	offset: constants.WS_HEADER_OFFSET,
	value: constants.WS_PACKAGE_HEADER_TOTAL_LENGTH,
},
{
	name: 'Protocol Version',
	key: 'ver',
	bytes: 2,
	offset: constants.WS_VERSION_OFFSET,
	value: constants.WS_HEADER_DEFAULT_VERSION,
},
{
	name: 'Operation',
	key: 'op',
	bytes: 4,
	offset: constants.WS_OPERATION_OFFSET,
	value: constants.WS_HEADER_DEFAULT_OPERATION,
},
{
	name: 'Sequence Id',
	key: 'seq',
	bytes: 4,
	offset: constants.WS_SEQUENCE_OFFSET,
	value: constants.WS_HEADER_DEFAULT_SEQUENCE,
}];