#include<stdio.h>
#include<stdlib.h>
#include<string.h>
#include<math.h>
#include <iostream>
#include <time.h>
#include "graph.h"
#include <sstream>
#include <sstream>
#include <vector>
#include <string>
#include <Windows.h>
#include <string>
#include <filesystem>
using namespace std;

int write_bat(int *cut_timing,int number_of_block,int noneedtime,int asshukutime,const char* inputmp4);
#define MAXTIME 158
#define MOVIETIME 159

int main(){

	double threshold = 0.7;
	double smooth = 1.3;
	int out[30000],out_sum[30000];
	int offset = 0, offset_m = 0;
	int end_offset = 0;
	double t_start=0;
	int favorite_part_number=3;
	int start_time[4];
	int end_time[4];

	int movie_time[3];

	double tmp;
	char flag[2];

	int tmp_1,tmp_2,tmp_3,tmp_4;

	printf("お気に入りだった作品数を入力してください(1～4)  例）3\n");
	scanf("%d",&favorite_part_number);

	for(int i=0;i<favorite_part_number;i++){
		printf("%d番目にお気に入りだった作品について\n",i+1);
		printf("鑑賞開始・終了時刻を入力してください  例）0:30-1:45\n");
		scanf("%d:%d-%d:%d",&tmp_1,&tmp_2,&tmp_3,&tmp_4);
		start_time[i]=tmp_1*60+tmp_2;
		end_time[i]=tmp_3*60+tmp_4;
	}

	printf("動画時間(分:秒)を入力してください  例）29:59\n");
	scanf("%d:%d",&movie_time[0],&movie_time[1]);
	movie_time[2]=movie_time[0]*60+movie_time[1];

	int favorite_time=end_time[0]-start_time[0];
	
	for(int favorite_part_number=1;
		favorite_time+end_time[favorite_part_number]-start_time[favorite_part_number]<MAXTIME;
		favorite_part_number++)
	{
		favorite_time+=end_time[favorite_part_number]-start_time[favorite_part_number];
	}

	printf("favorite_part_number:%d\n",favorite_part_number);
	printf("favorite_time:%d\n",favorite_time);

	std::vector<std::string> file_list_ac,file_list_movie;
	std::ostringstream oss_ac, oss_movie,oss_filename_ac,oss_filename_movie;

	// 再帰的にファイル名を取得する場合は、std::tr2::sys::recursive_directory_iteratorを使う
	for (std::tr2::sys::directory_iterator it("ac"), end; it != end; ++it) {
		file_list_ac.push_back(it->path());
	} 
	for (std::tr2::sys::directory_iterator it("movie"), end; it != end; ++it) {
		file_list_movie.push_back(it->path());
	}
    // 取得したファイル名をすべて表示する
    for (auto &path : file_list_ac) {
//        std::cout << path << std::endl;
        oss_ac << path;
    }
	// AC_20140704_120030.txt
    for (auto &path : file_list_movie) {
//        std::cout << path << std::endl;
        oss_movie << path;
    }
	//20140518_140551_945.mp4

	char ac_filename[30],actime[7];
	strcpy( ac_filename,oss_ac.str().c_str());
	strncpy( actime,&ac_filename[12],6);
//	printf("\n%s\n",actime);

	char movie_filename[30],motime[7];
	strcpy( movie_filename,oss_movie.str().c_str());
	strncpy( motime,&movie_filename[9],6);

	actime[6]=NULL;
	motime[6]=NULL;

	int ac = atoi(actime);
	int mo = atoi(motime);

	offset_m = -(ac/10000-mo/10000)*60*60 - ((ac%10000)/100-(mo%10000)/100)*60 - (ac%100 - mo%100);
	
	oss_filename_ac << "ac/" << oss_ac.str().c_str();
	oss_filename_movie << "movie/" << oss_movie.str().c_str();
	FILE *inputfile;         // 入力ストリーム
	inputfile = fopen(oss_filename_ac.str().c_str(), "r");
	if (inputfile == NULL) {          // オープンに失敗した場合
		printf("cannot open\n");         // エラーメッセージを出して
		exit(1);                         // 異常終了
	}
	int ret,length=0,counter=0;
	double t_raw[30000],value_raw[12000],value_tmp1,value_tmp2,value_tmp3,t[30000],value[30000];
	while( (ret = fscanf( inputfile , "%lf\t[%lf, %lf, %lf]" , &t_raw[length] , &value_tmp1,&value_tmp2,&value_tmp3 ) ) != EOF) {
		if(counter%8==0){
//			printf("value_tmp1:%lf\tvalue_tmp2:%lf\tvalue_tmp3:%lf\n",value_tmp1,value_tmp2,value_tmp3);
			value_raw[length]=abs(value_tmp1)+abs(value_tmp2)+abs(value_tmp3);
			length++;
		}
		counter++;
	}		
	fclose( inputfile );

	t_start=t_raw[0];
	for(int i=0;i<length;i++){
		t_raw[i]=(t_raw[i]-t_start)/1000;
	}
	for(int i = 0 ; i<length;i++){
		if(t_raw[i]<offset_m){
			offset=i;
		}
		if(t_raw[i]<movie_time[2]+offset_m){
			end_offset=i;
		}
	}

	length = 0;
	for(int i = offset ; i<end_offset;i++){
		value[i-offset]=value_raw[i];
		t[i-offset]=t_raw[i];
		length++;
	}

	printf("offset:%d\noffset_m:%d\nlength:%d\n",offset,offset_m,length);
///////////////////

	typedef Graph<double,double,double> GraphType;
	GraphType *g = new GraphType(length,1); 
	g -> add_node(length); 

	for(int i = 0;i <length;i++){
		if(value[i] < threshold){
			g -> add_tweights(i ,0,threshold-value[i]);
		}else{
			g -> add_tweights(i ,value[i]-threshold,0);			
		}
		if(i<length-1){
			g -> add_edge( i, i+1, smooth, smooth );
		}
	}

	int flow = g -> maxflow();

	for ( int i = 0; i < length ; i++ ) {
		if (g->what_segment(i) == GraphType::SOURCE){
			out[i]=0; //自動検出を一時的にoffにするため、1にした。本来0
		}else{
			out[i]=1;
		}
	}

	delete g;


//	printf("noneedtime:%d\nasshukutime:%d\nfavoritepartnumber:%d\n",noneedtime,asshukutime,favorite_part_number);

	/*
	printf("下記の入力でよろしいですか？[y/n]\n");
	for(int i=0;i<favorite_part_number;i++){
	printf("%d番目：%d\t%d\n",i+1,start_time[i],end_time[i]);
	}
	printf("動画時間：%d分%d秒",movie_time[0],movie_time[1]);
	scanf("%s",flag);
	if(strcmp(flag,"y")==0 || strcmp(flag,"Y")==0 ){
	break;
	}
	printf("再入力をお願いします\n");
	*/

	//ソート
	for(int i=0;i<favorite_part_number-1;i++){
		if(start_time[i]>start_time[i+1]){
			tmp=start_time[i];
			start_time[i]=start_time[i+1];
			start_time[i+1]=tmp;
			tmp=end_time[i];
			end_time[i]=end_time[i+1];
			end_time[i+1]=tmp;
		}
	}

	///////////////////////

	///////////////////////ここから自動認識//////////////////////

	for(int i=0;i<length;i++){
		out_sum[i]=0;
	}

/////////////////////ここ
	for(int k=0;k<length;k++){
		for(int i=0;i<favorite_part_number;i++){
			if(t[k]>=start_time[i] && t[k]<=end_time[i]){
				out_sum[k]=1;
			}
		}
	}
/////////////////////ここ


	for(int i=0;i<length;i++){
		out_sum[i]=out_sum[i]*out[i];
	}

	FILE *outputfile;         // 出力ストリーム
	outputfile = fopen("output.txt", "w");


	if (outputfile == NULL) {          // オープンに失敗した場合
		printf("cannot open\n");         // エラーメッセージを出して
		exit(1);                         // 異常終了
	}

	for(int i=0;i<length;i++){
		fprintf(outputfile,"%lf",t[i]);
		fprintf(outputfile,"\t%d",out[i]);
		fprintf(outputfile,"\t%d",out_sum[i]);
		fprintf(outputfile,"\n");
	}

	int cut_timing[50];
	int number_of_block=1;
	tmp=out_sum[0];

	cut_timing[0]=tmp;
	cut_timing[1]=0;

	for(int i=0;i<length;i++){
		if(tmp!=out_sum[i]){
			cut_timing[number_of_block+1]=t[i];
			number_of_block++;
			tmp=out_sum[i];
		}
	}
	cut_timing[number_of_block+1]=t[length-1];
	fclose( outputfile );

	double noneedtime=0;
	double asshukutime=0;

	noneedtime = (double)movie_time[2] - (double)favorite_time;
/*
	for(int i=0;i<length;i++){
		if(out_sum[i]==0){
			if(i!=0){
				noneedtime+=t[i]-t[i-1];
			}else{
				noneedtime+=0.1;
			}
		}
	}
	*/
	asshukutime =  (double)MOVIETIME - (double)( movie_time[2] - noneedtime ) ;

	printf("noneed:%lf\nasshuku:%lf\n movietime:%d\n",noneedtime,asshukutime,movie_time[2]);
	write_bat(cut_timing,number_of_block,noneedtime,asshukutime,oss_filename_movie.str().c_str());

	return 0;

}

int write_bat(int *cut_timing,int number_of_block,int noneedtime,int asshukutime,const char* inputmp4){
	int tmp=cut_timing[0];
	std::ostringstream oss;
//////ここから倍速//////////
	for(int i=1;i<=number_of_block;i++){
		oss <<"ffmpeg -i "<<inputmp4<<" -ss "<< cut_timing[i] <<" -t "<< cut_timing[i+1]-cut_timing[i]+1;
		if(tmp==1){
			oss	<< " tmp/ac_"<< (i+1)/2 <<".mp4\n\n";
			oss << "ffmpeg -i tmp/ac_"<< (i+1)/2 <<".mp4"
				<< " -vf \"scale=640:360,setpts=1*PTS\" -af \"atempo=1.0\" -vcodec libx264 -preset fast -vprofile main -qmax 51 -qmin 10 -keyint_min 0 -r 20 -strict -2"
				<<" tmp/ac_"<< (i+1)/2 <<"_1.mp4\n\n";
			tmp=0;
		}else{
			oss	<< " tmp/sp_"<< (i+1)/2 <<".mp4\n\n";
			oss << "ffmpeg -i tmp/sp_"<< (i+1)/2 <<".mp4"
				<< " -vf \"scale=640:360,setpts=1*PTS\" -af \"atempo=1.0\" -vcodec libx264 -preset fast -vprofile main -qmax 51 -qmin 10 -keyint_min 0 -r 20 -strict -2"
				<<" tmp/sp_"<< (i+1)/2 <<"_1.mp4\n\n";
			int j;
			for(j=1;j<=(double)(noneedtime/asshukutime/2.0);j=j*2){
				oss << "ffmpeg -i tmp/sp_"<< (i+1)/2 <<"_"<< j <<".mp4 ";
				oss	<< "-vf \"scale=640:360,setpts=1/2*PTS\" -af \"atempo=2.0\" -vcodec libx264 -preset fast -vprofile main -qmax 51 -qmin 10 -keyint_min 0 -r 20 -strict -2 "<<"tmp/sp_"<< (i+1)/2 <<"_"<< j*2<<".mp4\n\n";
			}
			oss << "ffmpeg -i tmp/sp_"<< (i+1)/2 <<"_"<< j <<".mp4 ";
			oss	<< "-vf \"scale=640:360,setpts="<< j*asshukutime << "/" <<noneedtime <<"*PTS\" -af \"atempo=" << (double)noneedtime/(double)(j*asshukutime) << "\" -vcodec libx264 -preset fast -vprofile main -qmax 51 -qmin 10 -keyint_min 0 -r 20 -strict -2 "<<"tmp/sp_"<< (i+1)/2 <<"_end.mp4\n\n";

			tmp=1;
		}
	}
//////ここまで倍速//////////


////ここから連結//////
	oss << "ffmpeg ";
	for(int i=1;i<=(number_of_block-1)/2;i++){
		if(cut_timing[0]==1){
		oss << "-i tmp/ac_"<< i <<"_1.mp4 ";
		oss << "-i tmp/sp_"<< i <<"_end.mp4 ";
		}else{
		oss << "-i tmp/sp_"<< i <<"_end.mp4 ";
		oss << "-i tmp/ac_"<< i <<"_1.mp4 ";
		}
	}
	if(number_of_block%2 == 1){
		if(cut_timing[0]==1){
		oss << "-i tmp/ac_"<< (number_of_block+1)/2 <<"_1.mp4 ";
		}else{
		oss << "-i tmp/sp_"<< (number_of_block+1)/2 <<"_end.mp4 ";
		}
	}else{
		if(cut_timing[0]==1){
		oss << "-i tmp/ac_"<< (number_of_block+1)/2 <<"_1.mp4 ";
		oss << "-i tmp/sp_"<< (number_of_block+1)/2 <<"_end.mp4 ";
		}else{
		oss << "-i tmp/sp_"<< (number_of_block+1)/2 <<"_end.mp4 ";
		oss << "-i tmp/ac_"<< (number_of_block+1)/2 <<"_1.mp4 ";
		}
	}
	oss << "-filter_complex \"concat=n=" << number_of_block <<":v=1:a=1\" tmp/in_fixed.mp4\n\n";
////ここまで連結//////

	FILE *outputbatfile;         // 出力ストリーム
	outputbatfile = fopen("movie_fix.bat", "w");
	if (outputbatfile == NULL) {          // オープンに失敗した場合
		printf("cannot open\n");         // エラーメッセージを出して
		exit(1);                         // 異常終了
	}
	fprintf(outputbatfile,oss.str().c_str());
	fclose(outputbatfile);
	return 0;
}


