unsigned long long adv = 0;
int tlen;
TYPE tto;
TYPE prlen;
TYPE pmin;
TYPE pmax;
int i, j;
char ch;
TYPE state = 0;
TYPE acc = -1;
int done;
const char* endsz = strchr(uri, '?');
size_t urilen;
bool result;
if (endsz) {
	urilen = endsz - uri + 1;
}
else {
	urilen = strlen(uri);
}
ch = adv >= urilen ? -1 : uri[adv++];
while (ch != -1) {
	result = false;
	acc = -1;
	done = 0;
	while (!done) {
	start_dfa:
		done = 1;
		acc = fsm_data[state++];
		tlen = fsm_data[state++];
		for (i = 0; i < tlen; ++i) {
			tto = fsm_data[state++];
			prlen = fsm_data[state++];
			for (j = 0; j < prlen; ++j) {
				pmin = fsm_data[state++];
				pmax = fsm_data[state++];
				if (ch < pmin) {
					break;
				}
				if (ch <= pmax) {
					result = true;
					ch = adv >= urilen ? -1 : uri[adv++];
					state = tto;
					done = 0;
					goto start_dfa;
				}
			}
		}
		if (acc != -1 && result) {
			if (adv==urilen) {
				return (int)acc;
			}
			return -1;
		}
		ch = adv >= urilen ? -1 : uri[adv++];
		state = 0;
	}
}
return -1;

