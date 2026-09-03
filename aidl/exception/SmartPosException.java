package com.persianswitch.smartpos.aidl.exception;

import android.os.Parcel;
import android.os.Parcelable;

public class SmartPosException extends Exception implements Parcelable {

    private int code;

    public SmartPosException(int code, String message) {
        super(message);
        this.code = code;
    }// constructor


    protected SmartPosException(Parcel in) {
        super(in.readString());
        code = in.readInt();
    }// constructor


    public int getCode() {
        return code;
    }// getCode

    public static final Creator<SmartPosException> CREATOR = new Creator<SmartPosException>() {
        @Override
        public SmartPosException createFromParcel(Parcel in) {
            return new SmartPosException(in);
        }

        @Override
        public SmartPosException[] newArray(int size) {
            return new SmartPosException[size];
        }
    };


    @Override
    public int describeContents() {
        return 0;
    }// describeContents


    @Override
    public void writeToParcel(Parcel dest, int flags) {
        dest.writeString( getMessage() );
        dest.writeInt(code);
    }// writeToParcel


}// SmartPosException class
